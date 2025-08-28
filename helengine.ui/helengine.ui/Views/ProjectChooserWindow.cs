using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using helengine.ui.Models;

namespace helengine.ui.Views {
    public class ProjectChooserWindow : Window {
        private readonly List<Project> _recentProjects = new();
        private ListBox? _projectListBox;
        private Project? _selectedProject;

        public Project? SelectedProject => _selectedProject;
        public event EventHandler<Project>? ProjectSelected;
        public event EventHandler? NewProjectRequested;
        public event EventHandler? BrowseProjectRequested;

        public ProjectChooserWindow() {
            InitializeWindow();
            LoadRecentProjects();
            SetupContent();
        }

        private void InitializeWindow() {
            Title = "helengine - select project";
            Width = 800;
            Height = 600;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            // 90s-inspired deep purple background
            Background = new SolidColorBrush(Color.FromRgb(25, 15, 35));
        }

        private void LoadRecentProjects() {
            // For now, create some sample projects
            // TODO: Load from persistent storage
            _recentProjects.AddRange(new[] {
                new Project {
                    Name = "Sample Game",
                    Path = @"C:\Projects\SampleGame",
                    LastOpened = DateTime.Now.AddDays(-1),
                    Description = "A simple 3D game project"
                },
                new Project {
                    Name = "Platformer Demo",
                    Path = @"C:\Projects\PlatformerDemo",
                    LastOpened = DateTime.Now.AddDays(-3),
                    Description = "2D platformer prototype"
                },
                new Project {
                    Name = "Engine Test",
                    Path = @"C:\Projects\EngineTest",
                    LastOpened = DateTime.Now.AddDays(-7),
                    Description = "Testing various engine features"
                }
            });

            // Sort by last opened (most recent first)
            _recentProjects.Sort((a, b) => b.LastOpened.CompareTo(a.LastOpened));
        }

        private void SetupContent() {
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star)); // Content
            mainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Footer

            // Header
            var headerPanel = CreateHeader();
            Grid.SetRow(headerPanel, 0);
            mainGrid.Children.Add(headerPanel);

            // Content area with projects
            var contentPanel = CreateContentArea();
            Grid.SetRow(contentPanel, 1);
            mainGrid.Children.Add(contentPanel);

            // Footer with action buttons
            var footerPanel = CreateFooter();
            Grid.SetRow(footerPanel, 2);
            mainGrid.Children.Add(footerPanel);

            Content = mainGrid;
        }

        private Panel CreateHeader() {
            var panel = new StackPanel {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(20),
                Background = new SolidColorBrush(Color.FromRgb(40, 25, 50)) // Darker purple gradient
            };

            var titleText = new TextBlock {
                Text = "helengine",
                FontSize = 32,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 102, 204)), // Hot pink, very 90s
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 15),
                FontFamily = new FontFamily("Consolas, 'Courier New', monospace") // Monospace for retro feel
            };

            var subtitleText = new TextBlock {
                Text = "select a project to open or create a new one",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 255, 153)), // Bright green
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15),
                FontFamily = new FontFamily("Consolas, 'Courier New', monospace")
            };

            panel.Children.Add(titleText);
            panel.Children.Add(subtitleText);

            return panel;
        }

        private Panel CreateContentArea() {
            var mainPanel = new DockPanel {
                Margin = new Thickness(20, 0)
            };

            // Recent projects section
            var recentProjectsPanel = CreateRecentProjectsPanel();
            DockPanel.SetDock(recentProjectsPanel, Dock.Left);
            mainPanel.Children.Add(recentProjectsPanel);

            // Quick actions panel
            var quickActionsPanel = CreateQuickActionsPanel();
            DockPanel.SetDock(quickActionsPanel, Dock.Right);
            mainPanel.Children.Add(quickActionsPanel);

            return mainPanel;
        }

        private Panel CreateRecentProjectsPanel() {
            var panel = new StackPanel {
                Width = 500,
                Margin = new Thickness(0, 0, 20, 0)
            };

            var headerText = new TextBlock {
                Text = "recent projects",
                FontSize = 16,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 102)), // Bright yellow
                Margin = new Thickness(0, 0, 0, 10),
                FontFamily = new FontFamily("Consolas, 'Courier New', monospace")
            };
            panel.Children.Add(headerText);

            // Project list
            _projectListBox = new ListBox {
                Background = new SolidColorBrush(Color.FromRgb(15, 5, 25)), // Dark purple
                BorderBrush = new SolidColorBrush(Color.FromRgb(102, 255, 255)), // Cyan border
                BorderThickness = new Thickness(2),
                Height = 400
            };

            _projectListBox.SelectionChanged += OnProjectSelectionChanged;
            _projectListBox.DoubleTapped += OnProjectDoubleClicked;

            foreach (var project in _recentProjects) {
                var projectItem = CreateProjectListItem(project);
                _projectListBox.Items.Add(projectItem);
            }

            panel.Children.Add(_projectListBox);

            return panel;
        }

        private Control CreateProjectListItem(Project project) {
            var border = new Border {
                Background = new SolidColorBrush(Color.FromRgb(30, 15, 40)), // Dark purple
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 102, 204)), // Hot pink border
                BorderThickness = new Thickness(1, 1, 1, 2),
                Padding = new Thickness(15, 10),
                Tag = project,
                Margin = new Thickness(2)
            };

            var stackPanel = new StackPanel {
                Orientation = Orientation.Vertical
            };

            var nameText = new TextBlock {
                Text = project.DisplayName.ToLower(),
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 255, 255)), // Bright cyan
                FontFamily = new FontFamily("Consolas, 'Courier New', monospace")
            };

            var pathText = new TextBlock {
                Text = project.Path.ToLower(),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 102)), // Bright yellow
                Margin = new Thickness(0, 2, 0, 0),
                FontFamily = new FontFamily("Consolas, 'Courier New', monospace")
            };

            var timeText = new TextBlock {
                Text = project.RelativeTime.ToLower(),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 255, 153)), // Bright green
                Margin = new Thickness(0, 4, 0, 0),
                FontFamily = new FontFamily("Consolas, 'Courier New', monospace")
            };

            stackPanel.Children.Add(nameText);
            stackPanel.Children.Add(pathText);
            stackPanel.Children.Add(timeText);

            if (!string.IsNullOrEmpty(project.Description)) {
                var descText = new TextBlock {
                    Text = project.Description.ToLower(),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 178, 102)), // Orange
                    Margin = new Thickness(0, 4, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("Consolas, 'Courier New', monospace")
                };
                stackPanel.Children.Add(descText);
            }

            border.Child = stackPanel;
            return border;
        }

        private Panel CreateQuickActionsPanel() {
            var panel = new StackPanel {
                Width = 200,
                Spacing = 15
            };

            var headerText = new TextBlock {
                Text = "quick actions",
                FontSize = 16,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 102)), // Bright yellow
                Margin = new Thickness(0, 0, 0, 10),
                FontFamily = new FontFamily("Consolas, 'Courier New', monospace")
            };
            panel.Children.Add(headerText);

            var newProjectBtn = CreateActionButton("new project", "create a new project from scratch");
            newProjectBtn.Click += (s, e) => NewProjectRequested?.Invoke(this, EventArgs.Empty);
            panel.Children.Add(newProjectBtn);

            var openProjectBtn = CreateActionButton("open project", "browse and open an existing project");
            openProjectBtn.Click += (s, e) => BrowseProjectRequested?.Invoke(this, EventArgs.Empty);
            panel.Children.Add(openProjectBtn);

            return panel;
        }

        private Button CreateActionButton(string title, string description) {
            var button = new Button {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Color.FromRgb(255, 102, 204)), // Hot pink
                BorderBrush = new SolidColorBrush(Color.FromRgb(102, 255, 255)), // Cyan border
                BorderThickness = new Thickness(2),
                Padding = new Thickness(15, 12),
                Margin = new Thickness(0, 5)
            };

            var stackPanel = new StackPanel();

            var titleText = new TextBlock {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(25, 15, 35)), // Dark purple text
                HorizontalAlignment = HorizontalAlignment.Center,
                FontFamily = new FontFamily("Consolas, 'Courier New', monospace")
            };

            var descText = new TextBlock {
                Text = description,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(40, 25, 50)), // Darker purple
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0),
                FontFamily = new FontFamily("Consolas, 'Courier New', monospace")
            };

            stackPanel.Children.Add(titleText);
            stackPanel.Children.Add(descText);
            button.Content = stackPanel;

            // Hover effects - swap colors
            button.PointerEntered += (s, e) => {
                button.Background = new SolidColorBrush(Color.FromRgb(102, 255, 255)); // Cyan
                titleText.Foreground = new SolidColorBrush(Color.FromRgb(255, 102, 204)); // Hot pink text
                descText.Foreground = new SolidColorBrush(Color.FromRgb(255, 102, 204));
            };
            button.PointerExited += (s, e) => {
                button.Background = new SolidColorBrush(Color.FromRgb(255, 102, 204)); // Hot pink
                titleText.Foreground = new SolidColorBrush(Color.FromRgb(25, 15, 35)); // Dark purple text
                descText.Foreground = new SolidColorBrush(Color.FromRgb(40, 25, 50));
            };

            return button;
        }

        private Panel CreateFooter() {
            var panel = new DockPanel {
                Margin = new Thickness(20),
                Height = 50
            };

            var openButton = new Button {
                Content = "open project",
                Width = 120,
                Height = 35,
                Background = new SolidColorBrush(Color.FromRgb(102, 255, 153)), // Bright green
                Foreground = new SolidColorBrush(Color.FromRgb(25, 15, 35)), // Dark purple text
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 255, 102)), // Yellow border
                BorderThickness = new Thickness(2),
                IsEnabled = false,
                FontFamily = new FontFamily("Consolas, 'Courier New', monospace"),
                FontWeight = FontWeight.Bold
            };
            openButton.Click += OnOpenProject;

            var cancelButton = new Button {
                Content = "cancel",
                Width = 80,
                Height = 35,
                Background = new SolidColorBrush(Color.FromRgb(255, 178, 102)), // Orange
                Foreground = new SolidColorBrush(Color.FromRgb(25, 15, 35)), // Dark purple text
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 102, 204)), // Hot pink border
                BorderThickness = new Thickness(2),
                Margin = new Thickness(10, 0, 0, 0),
                FontFamily = new FontFamily("Consolas, 'Courier New', monospace"),
                FontWeight = FontWeight.Bold
            };
            cancelButton.Click += (s, e) => Close();

            var buttonPanel = new StackPanel {
                Orientation = Orientation.Horizontal
            };
            buttonPanel.Children.Add(openButton);
            buttonPanel.Children.Add(cancelButton);

            DockPanel.SetDock(buttonPanel, Dock.Right);
            panel.Children.Add(buttonPanel);

            // Store reference to enable/disable
            openButton.Tag = "OpenButton";

            return panel;
        }

        private void OnProjectSelectionChanged(object? sender, SelectionChangedEventArgs e) {
            if (_projectListBox?.SelectedItem is Border border && border.Tag is Project project) {
                _selectedProject = project;
                
                // Enable the open button
                if (Content is Grid grid && 
                    grid.Children.OfType<Panel>().LastOrDefault() is DockPanel footer &&
                    footer.Children.OfType<StackPanel>().FirstOrDefault() is StackPanel buttonPanel &&
                    buttonPanel.Children.OfType<Button>().FirstOrDefault(b => b.Tag?.ToString() == "OpenButton") is Button openBtn) {
                    openBtn.IsEnabled = true;
                }
            }
        }

        private void OnProjectDoubleClicked(object? sender, EventArgs e) {
            if (_selectedProject != null) {
                ProjectSelected?.Invoke(this, _selectedProject);
            }
        }

        private void OnOpenProject(object? sender, EventArgs e) {
            if (_selectedProject != null) {
                ProjectSelected?.Invoke(this, _selectedProject);
            }
        }
    }
}

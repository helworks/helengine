using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using helengine.ui.Models;

namespace helengine.ui.Controls {
    public class ProjectListControl : UserControl {
        private ListBox? _projectListBox;
        private readonly List<Project> _projects = new();
        private Project? _selectedProject;

        public Project? SelectedProject => _selectedProject;
        public event EventHandler<Project>? ProjectSelected;

        public ProjectListControl() {
            InitializeComponent();
        }

        private void InitializeComponent() {
            var mainPanel = new DockPanel {
                Margin = new Thickness(20)
            };

            // Projects list
            var projectsPanel = new Border {
                Background = new SolidColorBrush(Color.FromRgb(40, 25, 50)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(102, 255, 255)),
                BorderThickness = new Thickness(2),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 20, 0, 0)
            };

            var projectsContainer = new StackPanel();

            var projectsHeader = new TextBlock {
                Text = "recent projects",
                FontSize = 16,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 102)),
                Margin = new Thickness(0, 0, 0, 15),
                FontFamily = new FontFamily("Consolas")
            };
            projectsContainer.Children.Add(projectsHeader);

            _projectListBox = new ListBox {
                Background = new SolidColorBrush(Color.FromRgb(40, 25, 50)),
                BorderThickness = new Thickness(0),
                MinHeight = 200,
                FontFamily = new FontFamily("Consolas")
            };
            _projectListBox.SelectionChanged += OnProjectSelectionChanged;
            _projectListBox.DoubleTapped += OnProjectDoubleClick;

            projectsContainer.Children.Add(_projectListBox);
            projectsPanel.Child = projectsContainer;
            mainPanel.Children.Add(projectsPanel);

            Content = mainPanel;
        }

        public void SetProjects(IEnumerable<Project> projects) {
            _projects.Clear();
            _projects.AddRange(projects);
            RefreshProjectList();
        }

        public void AddProject(Project project) {
            _projects.Insert(0, project); // Add to beginning
            RefreshProjectList();
        }

        private void RefreshProjectList() {
            if (_projectListBox == null) return;

            _projectListBox.Items.Clear();
            foreach (var project in _projects.OrderByDescending(p => p.LastOpened)) {
                var projectItem = CreateProjectListItem(project);
                _projectListBox.Items.Add(projectItem);
            }
        }

        private Border CreateProjectListItem(Project project) {
            var border = new Border {
                Background = new SolidColorBrush(Color.FromRgb(40, 25, 50)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 102, 204)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 2),
                Tag = project
            };

            var stackPanel = new StackPanel();

            var nameText = new TextBlock {
                Text = project.Name.ToLower(),
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 255, 255)),
                FontFamily = new FontFamily("Consolas")
            };

            var pathText = new TextBlock {
                Text = project.Path.ToLower(),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 102)),
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 2, 0, 0)
            };

            var timeText = new TextBlock {
                Text = project.RelativeTime.ToLower(),
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 255, 153)),
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 3, 0, 0)
            };

            var statsText = new TextBlock {
                Text = $"opened {project.TimesOpened} times",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 153, 102)),
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 1, 0, 0)
            };

            stackPanel.Children.Add(nameText);
            stackPanel.Children.Add(pathText);
            stackPanel.Children.Add(timeText);
            stackPanel.Children.Add(statsText);

            border.Child = stackPanel;

            // Hover effects
            border.PointerEntered += (s, e) => {
                border.Background = new SolidColorBrush(Color.FromRgb(255, 102, 204));
                nameText.Foreground = new SolidColorBrush(Color.FromRgb(25, 15, 35));
                pathText.Foreground = new SolidColorBrush(Color.FromRgb(25, 15, 35));
                timeText.Foreground = new SolidColorBrush(Color.FromRgb(25, 15, 35));
                statsText.Foreground = new SolidColorBrush(Color.FromRgb(25, 15, 35));
            };

            border.PointerExited += (s, e) => {
                border.Background = new SolidColorBrush(Color.FromRgb(40, 25, 50));
                nameText.Foreground = new SolidColorBrush(Color.FromRgb(102, 255, 255));
                pathText.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 102));
                timeText.Foreground = new SolidColorBrush(Color.FromRgb(102, 255, 153));
                statsText.Foreground = new SolidColorBrush(Color.FromRgb(255, 153, 102));
            };

            return border;
        }

        private void OnProjectSelectionChanged(object? sender, SelectionChangedEventArgs e) {
            if (_projectListBox?.SelectedItem is Border border && border.Tag is Project project) {
                _selectedProject = project;
            }
        }

        private void OnProjectDoubleClick(object? sender, EventArgs e) {
            if (_selectedProject != null) {
                ProjectSelected?.Invoke(this, _selectedProject);
            }
        }
    }
}

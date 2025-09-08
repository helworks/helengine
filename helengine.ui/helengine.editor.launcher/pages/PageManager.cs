using helengine;

namespace helengine.editor.launcher.pages {
    /// <summary>
    /// Manages page transitions and animations for the launcher
    /// </summary>
    public class PageManager {
        private Dictionary<string, LauncherPage> pages = new Dictionary<string, LauncherPage>();
        private LauncherPage? currentPage;
        private LauncherPage? transitioningPage;
        private int screenWidth;
        
        public string CurrentPageName { get; private set; } = "";
        public bool IsTransitioning => transitioningPage != null;
        
        public PageManager(int screenWidth) {
            this.screenWidth = screenWidth;
        }
        
        /// <summary>
        /// Register a page with the manager
        /// </summary>
        public void RegisterPage(string name, LauncherPage page) {
            pages[name] = page;
        }
        
        /// <summary>
        /// Navigate to a specific page with animation
        /// </summary>
        public void NavigateTo(string pageName, Action? onComplete = null) {
            if (!pages.ContainsKey(pageName)) {
                throw new ArgumentException($"Page '{pageName}' not found");
            }
            
            if (IsTransitioning) {
                return; // Don't allow navigation during transition
            }
            
            var targetPage = pages[pageName];
            
            if (currentPage == targetPage) {
                return; // Already on this page
            }
            
            transitioningPage = targetPage;
            
            // If there's a current page, hide it first
            if (currentPage != null) {
                currentPage.Hide(screenWidth, () => {
                    // After hide animation completes, show the new page
                    ShowNewPage(pageName, targetPage, onComplete);
                });
            } else {
                // No current page, just show the new one
                ShowNewPage(pageName, targetPage, onComplete);
            }
        }
        
        /// <summary>
        /// Show the initial page without animation
        /// </summary>
        public void ShowInitialPage(string pageName) {
            if (!pages.ContainsKey(pageName)) {
                throw new ArgumentException($"Page '{pageName}' not found");
            }
            
            var page = pages[pageName];
            currentPage = page;
            CurrentPageName = pageName;
            
            // Create and show immediately without animation so Hide() can run later
            page.ShowImmediate(screenWidth);
        }
        
        /// <summary>
        /// Update all active pages (for animations)
        /// </summary>
        public void Update() {
            currentPage?.Update();
            transitioningPage?.Update();
        }
        
        /// <summary>
        /// Update screen size for animations
        /// </summary>
        public void UpdateScreenSize(int newWidth) {
            screenWidth = newWidth;
            
            // Update all registered pages
            foreach (var page in pages.Values) {
                page.UpdateScreenWidth(newWidth);
            }
        }
        
        /// <summary>
        /// Get the current active page
        /// </summary>
        public LauncherPage? GetCurrentPage() {
            return currentPage;
        }
        
        /// <summary>
        /// Get a specific page by name
        /// </summary>
        public LauncherPage? GetPage(string name) {
            return pages.TryGetValue(name, out var page) ? page : null;
        }
        
        /// <summary>
        /// Clean up all pages
        /// </summary>
        public void Dispose() {
            foreach (var page in pages.Values) {
                page.DestroyPage();
            }
            pages.Clear();
            currentPage = null;
            transitioningPage = null;
        }
        
        private void ShowNewPage(string pageName, LauncherPage targetPage, Action? onComplete) {
            targetPage.Show(screenWidth, () => {
                // Transition complete
                currentPage = targetPage;
                CurrentPageName = pageName;
                transitioningPage = null;
                onComplete?.Invoke();
            });
        }
    }
}

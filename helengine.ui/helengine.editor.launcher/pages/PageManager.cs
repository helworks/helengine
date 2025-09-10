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
        private Stack<string> history = new Stack<string>();
        private KeyboardState lastKeyboard;
        
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
            NavigateTo(pageName, reverse: false, onComplete);
        }

        public void NavigateTo(string pageName, bool reverse, Action? onComplete = null) {
            NavigateTo(pageName, reverse, onComplete, recordHistory: true);
        }

        public void NavigateTo(string pageName, bool reverse, Action? onComplete, bool recordHistory) {
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
                if (recordHistory && !string.IsNullOrEmpty(CurrentPageName)) {
                    history.Push(CurrentPageName);
                }
                var hideDir = reverse ? SlideDirection.LeftToRight : SlideDirection.RightToLeft;
                currentPage.Hide(screenWidth, hideDir, () => {
                    // After hide animation completes, show the new page
                    ShowNewPage(pageName, targetPage, reverse, onComplete);
                });
            } else {
                // No current page, just show the new one
                ShowNewPage(pageName, targetPage, reverse, onComplete);
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
            history.Clear();
            
            // Create and show immediately without animation so Hide() can run later
            page.ShowImmediate(screenWidth);
        }
        
        /// <summary>
        /// Update all active pages (for animations)
        /// </summary>
        public void Update() {
            currentPage?.Update();
            transitioningPage?.Update();

            // ESC handling for back navigation
            var kb = Core.Instance.InputManager.Keyboard.GetState();
            bool escPressed = kb.IsKeyDown(Keys.Escape) && !lastKeyboard.IsKeyDown(Keys.Escape);
            bool f6Pressed = kb.IsKeyDown(Keys.F6) && !lastKeyboard.IsKeyDown(Keys.F6);
            bool f7Pressed = kb.IsKeyDown(Keys.F7) && !lastKeyboard.IsKeyDown(Keys.F7);
            lastKeyboard = kb;

            if (escPressed && !IsTransitioning && history.Count > 0) {
                var prev = history.Pop();
                // Reverse animation when going back; don't push current into history again
                NavigateTo(prev, reverse: true, onComplete: null, recordHistory: false);
            }

            // Toggle UI backend (debug)
            if (f6Pressed || f7Pressed) {
                var rm = Core.Instance.RenderManager3D as helengine.sharpdx.SharpDXRenderManager3D;
                if (rm != null) {
                    // Cycle modes: sdf -> nineslice -> geometry -> sdf ...
                    cycleModeIndex = (cycleModeIndex + (f6Pressed ? 1 : -1) + 3) % 3;
                    string mode = cycleModeIndex == 0 ? "sdf" : (cycleModeIndex == 1 ? "nineslice" : "geometry");
                    rm.SetUIBackend(mode);
                }
            }
        }

        int cycleModeIndex = 0;
        
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
        
        private void ShowNewPage(string pageName, LauncherPage targetPage, bool reverse, Action? onComplete) {
            var showDir = reverse ? SlideDirection.LeftToRight : SlideDirection.RightToLeft;
            targetPage.Show(screenWidth, showDir, () => {
                // Transition complete
                currentPage = targetPage;
                CurrentPageName = pageName;
                transitioningPage = null;
                onComplete?.Invoke();
            });
        }
    }
}

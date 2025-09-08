using helengine;

namespace helengine.editor.launcher.pages {
    /// <summary>
    /// Base class for launcher pages with animation support
    /// </summary>
    public abstract class LauncherPage {
        protected List<Entity> pageEntities = new List<Entity>();
        protected FontAsset font;
        protected bool isVisible;
        protected bool isAnimating;
        
        // Animation properties
        protected float targetX;
        protected float currentX;
        protected DateTime animationStartTime;
        protected float animationDuration = 0.3f; // 300ms animations
        protected Action? animationCompletionCallback;
        protected int screenWidth = 1280; // Default, updated by PageManager
        
        public bool IsVisible => isVisible;
        public bool IsAnimating => isAnimating;
        
        public LauncherPage(FontAsset font) {
            this.font = font;
        }
        
        /// <summary>
        /// Create all UI elements for this page
        /// </summary>
        public abstract void CreatePage();
        
        /// <summary>
        /// Clean up all UI elements
        /// </summary>
        public virtual void DestroyPage() {
            foreach (var entity in pageEntities) {
                entity?.Dispose();
            }
            pageEntities.Clear();
        }
        
        /// <summary>
        /// Show page with slide-in animation from the right
        /// </summary>
        public virtual void Show(int screenWidth, Action? onComplete = null) {
            if (isVisible) return;
            
            this.screenWidth = screenWidth;
            isVisible = true;
            isAnimating = true;
            
            // Start off-screen to the right
            currentX = screenWidth;
            targetX = 0;
            animationStartTime = DateTime.Now;
            
            CreatePage();
            UpdatePagePosition();
            
            // Start animation with completion callback
            animationCompletionCallback = onComplete;
        }

        /// <summary>
        /// Show page immediately without animation (used for initial page)
        /// </summary>
        public virtual void ShowImmediate(int screenWidth) {
            if (isVisible) return;

            this.screenWidth = screenWidth;
            isVisible = true;
            isAnimating = false;

            // Ensure page is created and positioned at rest
            currentX = 0;
            targetX = 0;
            CreatePage();
            UpdatePagePosition();
        }
        
        /// <summary>
        /// Hide page with slide-out animation to the left
        /// </summary>
        public virtual void Hide(int screenWidth, Action? onComplete = null) {
            
            this.screenWidth = screenWidth;
            isAnimating = true;
            targetX = -screenWidth;
            animationStartTime = DateTime.Now;
            
            // Set completion callback to clean up and notify
            animationCompletionCallback = () => {
                isVisible = false;
                DestroyPage();
                onComplete?.Invoke();
            };
        }
        
        /// <summary>
        /// Update animation frame
        /// </summary>
        public virtual void Update() {
            if (!isAnimating) return;
            
            float elapsed = (float)(DateTime.Now - animationStartTime).TotalSeconds;
            float progress = Math.Min(elapsed / animationDuration, 1.0f);
            
            // Ease-out animation curve
            progress = 1.0f - (1.0f - progress) * (1.0f - progress);
            
            // Determine start position based on target
            float startX;
            if (targetX == 0) {
                // Showing: start from screen width, end at 0
                startX = screenWidth;
            } else {
                // Hiding: start from 0, end at negative screen width
                startX = 0;
            }
            
            currentX = startX + (targetX - startX) * progress;
            
            UpdatePagePosition();
            
            if (progress >= 1.0f) {
                isAnimating = false;
                currentX = targetX;
                UpdatePagePosition();
                
                // Call completion callback
                animationCompletionCallback?.Invoke();
                animationCompletionCallback = null;
            }
        }
        
        /// <summary>
        /// Update all entity positions based on current animation position
        /// </summary>
        protected virtual void UpdatePagePosition() {
            // This method should be overridden by derived classes to set absolute positions
            // including the currentX animation offset
        }
        
        /// <summary>
        /// Start slide animation (to be overridden for custom animation handling)
        /// </summary>
        protected virtual void StartSlideAnimation(Action? onComplete) {
            // Animation will be handled in Update() method
            // onComplete will be called when animation finishes
        }
        
        /// <summary>
        /// Handle page-specific navigation events
        /// </summary>
        public abstract void OnNavigateTo(string targetPage);
        
        /// <summary>
        /// Get the absolute position for entities (including animation offset)
        /// </summary>
        protected float3 GetPosition(float x, float y, float z = 0) {
            return new float3(x + currentX, y, z);
        }
        
        /// <summary>
        /// Update screen width for responsive animations
        /// </summary>
        public virtual void UpdateScreenWidth(int newScreenWidth) {
            screenWidth = newScreenWidth;
        }
    }
}

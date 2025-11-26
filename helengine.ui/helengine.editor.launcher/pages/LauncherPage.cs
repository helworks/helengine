using helengine;

namespace helengine.editor.launcher.pages {
    public enum SlideDirection {
        RightToLeft, // enter from right, exit to left
        LeftToRight  // enter from left,  exit to right
    }
    /// <summary>
    /// Base class for launcher pages with animation support
    /// </summary>
    public abstract class LauncherPage {
        protected List<Entity> pageEntities = new List<Entity>();
        private readonly List<(Entity entity, float x, float y, float z)> positioned = new();
        protected FontAsset font;
        protected bool isVisible;
        protected bool isAnimating;
        
        // Animation properties
        protected float currentX;
        protected float animStartX;
        protected float animTargetX;
        protected DateTime animationStartTime;
        protected float animationDuration = 0.15f; // 50% faster than 0.3s
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
                CleanupEntity(entity);
            }
            pageEntities.Clear();
            positioned.Clear();
        }
        
        /// <summary>
        /// Show page with slide-in animation from the right
        /// </summary>
        public virtual void Show(int screenWidth, Action? onComplete = null) {
            Show(screenWidth, SlideDirection.RightToLeft, onComplete);
        }

        public virtual void Show(int screenWidth, SlideDirection direction, Action? onComplete = null) {
            if (isVisible) return;
            
            this.screenWidth = screenWidth;
            isVisible = true;
            isAnimating = true;
            
            // Configure animation based on direction
            animStartX = direction == SlideDirection.RightToLeft ? screenWidth : -screenWidth;
            animTargetX = 0;
            currentX = animStartX;
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
            animStartX = 0;
            animTargetX = 0;
            CreatePage();
            UpdatePagePosition();
            // No animation; fire on-shown hooks immediately
            FlushOnShownActions();
        }
        
        /// <summary>
        /// Hide page with slide-out animation to the left
        /// </summary>
        public virtual void Hide(int screenWidth, Action? onComplete = null) {
            Hide(screenWidth, SlideDirection.RightToLeft, onComplete);
        }

        public virtual void Hide(int screenWidth, SlideDirection direction, Action? onComplete = null) {
            this.screenWidth = screenWidth;
            isAnimating = true;
            animStartX = currentX; // usually 0 when visible
            animTargetX = direction == SlideDirection.RightToLeft ? -screenWidth : screenWidth;
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
            
            currentX = animStartX + (animTargetX - animStartX) * progress;
            
            UpdatePagePosition();
            
            if (progress >= 1.0f) {
                isAnimating = false;
                currentX = animTargetX;
                UpdatePagePosition();
                // Ensure any page-level post-show hooks run now
                FlushOnShownActions();

                // Call completion callback
                animationCompletionCallback?.Invoke();
                animationCompletionCallback = null;
            }
        }
        
        /// <summary>
        /// Update all entity positions based on current animation position
        /// </summary>
        protected virtual void UpdatePagePosition() {
            for (int i = 0; i < positioned.Count; i++) {
                var p = positioned[i];
                if (p.entity != null) {
                    p.entity.Position = GetPosition(p.x, p.y, p.z);
                }
            }
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
        /// Track an entity with a logical base position; the page animation offset is applied automatically.
        /// </summary>
        protected void AddPageEntity(Entity entity, float x, float y, float z = 0) {
            pageEntities.Add(entity);
            positioned.Add((entity, x, y, z));
            entity.Position = GetPosition(x, y, z);
        }

        private List<Action>? onShownActions;
        /// <summary>
        /// Queue an action to run when the page finishes showing (or immediately for ShowImmediate)
        /// </summary>
        protected void OnShown(Action action) {
            onShownActions ??= new List<Action>();
            onShownActions.Add(action);
        }

        private void FlushOnShownActions() {
            if (onShownActions == null) return;
            foreach (var a in onShownActions) {
                try { a(); } catch { }
            }
            onShownActions.Clear();
        }
        
        /// <summary>
        /// Update screen width for responsive animations
        /// </summary>
        public virtual void UpdateScreenWidth(int newScreenWidth) {
            screenWidth = newScreenWidth;
        }

        void CleanupEntity(Entity? entity) {
            if (entity == null) {
                return;
            }

            DisableAnchors(entity);

            if (entity.Children != null) {
                for (int i = 0; i < entity.Children.Count; i++) {
                    CleanupEntity(entity.Children[i]);
                }
            }

            // Disable to unregister from render/update/interactables
            entity.Enabled = false;
            Core.Instance.ObjectManager.RemoveEntity(entity);
        }

        void DisableAnchors(Entity entity) {
            if (entity.Components == null) {
                return;
            }

            for (int i = 0; i < entity.Components.Count; i++) {
                if (entity.Components[i] is AnchorComponent anchor) {
                    anchor.DisableAnchoring();
                }
            }
        }
    }
}

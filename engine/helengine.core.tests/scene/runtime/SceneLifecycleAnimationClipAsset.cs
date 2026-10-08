using helengine;

namespace helengine.core.tests.scene.runtime {
    /// <summary>
    /// Records whether all animation consumers have been disposed before scene-owned clip cleanup runs.
    /// </summary>
    public sealed class SceneLifecycleAnimationClipAsset : AnimationClipAsset {
        /// <summary>Roots whose lifecycle must end before the clip's final release.</summary>
        readonly IReadOnlyList<Entity> Roots;

        /// <summary>Captures the roots that borrow this test clip.</summary>
        /// <param name="roots">Roots that must be disposed before clip cleanup.</param>
        public SceneLifecycleAnimationClipAsset(IReadOnlyList<Entity> roots) {
            Roots = roots;
        }

        /// <summary>Gets the number of release callbacks observed by this clip.</summary>
        public int DisposeCount { get; private set; }

        /// <summary>Gets whether every borrowing root had already completed disposal at release.</summary>
        public bool RootsWereDisposedAtRelease { get; private set; }

        /// <summary>Records consumer lifetime and then performs ordinary animation payload cleanup.</summary>
        public override void Dispose() {
            DisposeCount++;
            RootsWereDisposedAtRelease = Roots.All(root => root.IsDisposed);
            base.Dispose();
        }
    }
}

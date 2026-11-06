using helengine;
using helengine.animation.runtime;

[assembly: GeneratedRuntimeModuleManifest(
    "animation-runtime-module",
    typeof(AnimationRuntimeRegistration),
    nameof(AnimationRuntimeRegistration.Register),
    typeof(HierarchyAnimationPlayerComponent))]

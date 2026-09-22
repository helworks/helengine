using System.Runtime.CompilerServices;
using helengine;

[assembly: InternalsVisibleTo("helengine.helphysics.tests")]
[assembly: GeneratedRuntimeModuleManifest(
    "helphysics-runtime-module",
    typeof(HelPhysicsRuntimeComponentRegistration),
    nameof(HelPhysicsRuntimeComponentRegistration.Register),
    typeof(RigidBody3DComponent),
    typeof(BoxCollider3DComponent),
    typeof(SphereCollider3DComponent),
    typeof(KinematicMotion3DComponent),
    typeof(CharacterController3DComponent),
    typeof(SceneEntityTriggerObserverComponent))]

using helengine;

[assembly: GeneratedRuntimeModuleManifest(
    "physics3d-runtime-module",
    typeof(BepuRuntimeComponentRegistration),
    nameof(BepuRuntimeComponentRegistration.Register),
    typeof(RigidBody3DComponent),
    typeof(BoxCollider3DComponent),
    typeof(SphereCollider3DComponent),
    typeof(CapsuleCollider3DComponent),
    typeof(StaticMeshCollider3DComponent),
    typeof(KinematicMotion3DComponent),
    typeof(CharacterController3DComponent))]

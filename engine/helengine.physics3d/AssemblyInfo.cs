using helengine;

[assembly: GeneratedRuntimeModuleManifest(
    "physics3d-runtime-module",
    typeof(Physics3DRuntimeComponentRegistration),
    nameof(Physics3DRuntimeComponentRegistration.Register),
    typeof(RigidBody3DComponent),
    typeof(BoxCollider3DComponent),
    typeof(SphereCollider3DComponent),
    typeof(CapsuleCollider3DComponent),
    typeof(StaticMeshCollider3DComponent),
    typeof(KinematicMotion3DComponent),
    typeof(CharacterController3DComponent))]

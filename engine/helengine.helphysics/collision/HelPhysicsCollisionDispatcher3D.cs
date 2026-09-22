namespace helengine {
    /// <summary>Dispatches primitive shape pairs into the existing narrow phase without allocating.</summary>
    static class HelPhysicsCollisionDispatcher3D {
        /// <summary>Builds a manifold for a supported shape pair in canonical body order.</summary>
        public static bool TryBuild(
            HelPhysicsShapePool3D shapes,
            HelPhysicsBoxCollisionScratch3D scratch,
            in HelPhysicsBodyColdState3D coldA,
            in HelPhysicsBodyState3D bodyA,
            in HelPhysicsBodyColdState3D coldB,
            in HelPhysicsBodyState3D bodyB,
            ref HelPhysicsContactManifold3D manifold) {
            if (coldA.ShapeKind == HelPhysicsShapeKind3D.Box && coldB.ShapeKind == HelPhysicsShapeKind3D.Box) {
                ref HelPhysicsBoxShape3D boxA = ref shapes.GetRequiredBox(coldA.ShapeHandle);
                ref HelPhysicsBoxShape3D boxB = ref shapes.GetRequiredBox(coldB.ShapeHandle);
                return HelPhysicsBoxBoxCollision3D.TryBuildManifold(in boxA, in bodyA, in boxB, in bodyB, scratch, ref manifold);
            } else if (coldA.ShapeKind == HelPhysicsShapeKind3D.Sphere && coldB.ShapeKind == HelPhysicsShapeKind3D.Sphere) {
                ref HelPhysicsSphereShape3D sphereA = ref shapes.GetRequiredSphere(coldA.ShapeHandle);
                ref HelPhysicsSphereShape3D sphereB = ref shapes.GetRequiredSphere(coldB.ShapeHandle);
                return HelPhysicsSphereCollision3D.TryBuildSphereSphere(in sphereA, in bodyA, in sphereB, in bodyB, ref manifold);
            } else if (coldA.ShapeKind == HelPhysicsShapeKind3D.Sphere) {
                ref HelPhysicsSphereShape3D sphereA = ref shapes.GetRequiredSphere(coldA.ShapeHandle);
                ref HelPhysicsBoxShape3D boxB = ref shapes.GetRequiredBox(coldB.ShapeHandle);
                return HelPhysicsSphereCollision3D.TryBuildSphereBox(in sphereA, in bodyA, in boxB, in bodyB, ref manifold);
            } else {
                ref HelPhysicsBoxShape3D boxA = ref shapes.GetRequiredBox(coldA.ShapeHandle);
                ref HelPhysicsSphereShape3D sphereB = ref shapes.GetRequiredSphere(coldB.ShapeHandle);
                return HelPhysicsSphereCollision3D.TryBuildBoxSphere(in boxA, in bodyA, in sphereB, in bodyB, ref manifold);
            }
        }
    }
}
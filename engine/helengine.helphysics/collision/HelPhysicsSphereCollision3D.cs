namespace helengine {
    /// <summary>Builds deterministic sphere pair and sphere versus oriented box contacts.</summary>
    static class HelPhysicsSphereCollision3D {
        /// <summary>Builds one contact for touching or overlapping spheres.</summary>
        public static bool TryBuildSphereSphere(
            in HelPhysicsSphereShape3D sphereA,
            in HelPhysicsBodyState3D bodyA,
            in HelPhysicsSphereShape3D sphereB,
            in HelPhysicsBodyState3D bodyB,
            ref HelPhysicsContactManifold3D manifold) {
            manifold.Reset();
            PhysicsVector3 offset = bodyB.Position - bodyA.Position;
            PhysicsScalar distanceSquared = offset.LengthSquared();
            PhysicsScalar radiusSum = sphereA.Radius + sphereB.Radius;
            if (distanceSquared > radiusSum * radiusSum) {
                return false;
            }

            PhysicsVector3 normal;
            PhysicsScalar distance;
            if (distanceSquared == PhysicsScalar.Zero) {
                normal = PhysicsVector3.UnitX;
                distance = PhysicsScalar.Zero;
            } else {
                distance = PhysicsScalar.Sqrt(distanceSquared);
                normal = offset / distance;
            }

            PhysicsScalar penetration = radiusSum - distance;
            PhysicsVector3 anchorA = bodyA.Position + (normal * sphereA.Radius);
            PhysicsVector3 anchorB = bodyB.Position - (normal * sphereB.Radius);
            PhysicsVector3 point = (anchorA + anchorB) * PhysicsScalar.FromFloat(0.5f);
            manifold.Contact0 = new HelPhysicsContactPoint3D(
                point, normal, ToLocalAnchor(point, in bodyA), ToLocalAnchor(point, in bodyB),
                penetration, new HelPhysicsContactFeature3D(0x30000000u));
            manifold.ContactCount = 1;
            return true;
        }

        /// <summary>Builds one contact between a sphere and an oriented box.</summary>
        public static bool TryBuildSphereBox(
            in HelPhysicsSphereShape3D sphere,
            in HelPhysicsBodyState3D sphereBody,
            in HelPhysicsBoxShape3D box,
            in HelPhysicsBodyState3D boxBody,
            ref HelPhysicsContactManifold3D manifold) {
            manifold.Reset();
            PhysicsVector3 localCenter = boxBody.Orientation.Conjugated().Rotate(sphereBody.Position - boxBody.Position);
            PhysicsVector3 extents = box.HalfExtents;
            PhysicsVector3 closest = new PhysicsVector3(
                PhysicsScalar.Clamp(localCenter.X, -extents.X, extents.X),
                PhysicsScalar.Clamp(localCenter.Y, -extents.Y, extents.Y),
                PhysicsScalar.Clamp(localCenter.Z, -extents.Z, extents.Z));
            PhysicsVector3 localDelta = localCenter - closest;
            PhysicsScalar distanceSquared = localDelta.LengthSquared();
            PhysicsVector3 normalLocal;
            PhysicsScalar penetration;
            PhysicsVector3 localBoxAnchor;
            int featureAxis;

            if (distanceSquared > PhysicsScalar.Zero) {
                PhysicsScalar distance = PhysicsScalar.Sqrt(distanceSquared);
                normalLocal = -localDelta / distance;
                penetration = sphere.Radius - distance;
                if (penetration < PhysicsScalar.Zero) {
                    return false;
                }

                localBoxAnchor = closest;
                featureAxis = AxisIndex(normalLocal);
            } else {
                PhysicsScalar distanceX = extents.X - PhysicsScalar.Abs(localCenter.X);
                PhysicsScalar distanceY = extents.Y - PhysicsScalar.Abs(localCenter.Y);
                PhysicsScalar distanceZ = extents.Z - PhysicsScalar.Abs(localCenter.Z);
                int axisIndex = 0;
                PhysicsScalar minimumDistance = distanceX;
                if (distanceY < minimumDistance) {
                    axisIndex = 1;
                    minimumDistance = distanceY;
                }
                if (distanceZ < minimumDistance) {
                    axisIndex = 2;
                    minimumDistance = distanceZ;
                }

                PhysicsScalar signedCoordinate = axisIndex == 0 ? localCenter.X : axisIndex == 1 ? localCenter.Y : localCenter.Z;
                PhysicsScalar sign = signedCoordinate >= PhysicsScalar.Zero ? PhysicsScalar.FromFloat(-1f) : PhysicsScalar.One;
                normalLocal = axisIndex == 0
                    ? new PhysicsVector3(sign, PhysicsScalar.Zero, PhysicsScalar.Zero)
                    : axisIndex == 1
                        ? new PhysicsVector3(PhysicsScalar.Zero, sign, PhysicsScalar.Zero)
                        : new PhysicsVector3(PhysicsScalar.Zero, PhysicsScalar.Zero, sign);
                penetration = sphere.Radius + minimumDistance;
                localBoxAnchor = localCenter - (normalLocal * minimumDistance);
                featureAxis = axisIndex;
            }

            PhysicsVector3 normalSphereToBox = boxBody.Orientation.Rotate(normalLocal);
            PhysicsVector3 worldBoxAnchor = boxBody.Position + boxBody.Orientation.Rotate(localBoxAnchor);
            PhysicsVector3 worldSphereAnchor = sphereBody.Position + (normalSphereToBox * sphere.Radius);
            PhysicsVector3 point = (worldSphereAnchor + worldBoxAnchor) * PhysicsScalar.FromFloat(0.5f);
            manifold.Contact0 = new HelPhysicsContactPoint3D(
                point, normalSphereToBox, ToLocalAnchor(point, in sphereBody), ToLocalAnchor(point, in boxBody),
                penetration, new HelPhysicsContactFeature3D(0x31000000u | (uint)featureAxis));
            manifold.ContactCount = 1;
            return true;
        }

        /// <summary>Builds a sphere-box manifold in box-sphere body order.</summary>
        public static bool TryBuildBoxSphere(
            in HelPhysicsBoxShape3D box,
            in HelPhysicsBodyState3D boxBody,
            in HelPhysicsSphereShape3D sphere,
            in HelPhysicsBodyState3D sphereBody,
            ref HelPhysicsContactManifold3D manifold) {
            HelPhysicsContactManifold3D sphereFirst = default;
            if (!TryBuildSphereBox(in sphere, in sphereBody, in box, in boxBody, ref sphereFirst)) {
                manifold.Reset();
                return false;
            }

            HelPhysicsContactPoint3D contact = sphereFirst.Contact0;
            manifold.Reset();
            manifold.Contact0 = new HelPhysicsContactPoint3D(
                contact.Position, -contact.Normal, contact.LocalAnchorB, contact.LocalAnchorA,
                contact.PenetrationDepth, contact.Feature);
            manifold.ContactCount = 1;
            return true;
        }

        /// <summary>Converts a world contact position to one body's local anchor frame.</summary>
        static PhysicsVector3 ToLocalAnchor(PhysicsVector3 worldPoint, in HelPhysicsBodyState3D body) {
            return body.Orientation.Conjugated().Rotate(worldPoint - body.Position);
        }

        /// <summary>Returns the dominant axis of a local normal for stable feature packing.</summary>
        static int AxisIndex(PhysicsVector3 normal) {
            PhysicsScalar x = PhysicsScalar.Abs(normal.X);
            PhysicsScalar y = PhysicsScalar.Abs(normal.Y);
            PhysicsScalar z = PhysicsScalar.Abs(normal.Z);
            if (x >= y && x >= z) {
                return 0;
            } else if (y >= z) {
                return 1;
            }

            return 2;
        }
    }
}
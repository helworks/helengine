using BepuPhysics;
using BepuPhysics.Collidables;

namespace helengine {
    /// <summary>
    /// Stores one BEPU body or static handle associated with one entity.
    /// </summary>
    public sealed class BepuBodyHandle3D {
        /// <summary>
        /// Initializes one box-backed dynamic or kinematic runtime body handle.
        /// </summary>
        /// <param name="entity">Entity owning the body.</param>
        /// <param name="rigidBody">Authored rigid-body component bound to the runtime handle.</param>
        /// <param name="boxCollider">Authored box collider attached to the entity.</param>
        /// <param name="shapeIndex">Shape index allocated in the active BEPU simulation.</param>
        /// <param name="bodyHandle">BEPU dynamic or kinematic body handle.</param>
        public BepuBodyHandle3D(
            Entity entity,
            RigidBody3DComponent rigidBody,
            BoxCollider3DComponent boxCollider,
            TypedIndex shapeIndex,
            BodyHandle bodyHandle) {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            RigidBody = rigidBody ?? throw new ArgumentNullException(nameof(rigidBody));
            BoxCollider = boxCollider ?? throw new ArgumentNullException(nameof(boxCollider));
            SphereCollider = null;
            StaticMeshCollider = null;
            ShapeIndex = shapeIndex;
            BodyHandle = bodyHandle;
            HasBodyHandle = true;
            StaticHandle = default;
            HasStaticHandle = false;
        }

        /// <summary>
        /// Initializes one box-backed static runtime body handle.
        /// </summary>
        /// <param name="entity">Entity owning the body.</param>
        /// <param name="rigidBody">Authored rigid-body component bound to the runtime handle.</param>
        /// <param name="boxCollider">Authored box collider attached to the entity.</param>
        /// <param name="shapeIndex">Shape index allocated in the active BEPU simulation.</param>
        /// <param name="staticHandle">BEPU static handle.</param>
        public BepuBodyHandle3D(
            Entity entity,
            RigidBody3DComponent rigidBody,
            BoxCollider3DComponent boxCollider,
            TypedIndex shapeIndex,
            StaticHandle staticHandle) {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            RigidBody = rigidBody ?? throw new ArgumentNullException(nameof(rigidBody));
            BoxCollider = boxCollider ?? throw new ArgumentNullException(nameof(boxCollider));
            SphereCollider = null;
            StaticMeshCollider = null;
            ShapeIndex = shapeIndex;
            BodyHandle = default;
            HasBodyHandle = false;
            StaticHandle = staticHandle;
            HasStaticHandle = true;
        }

        /// <summary>
        /// Initializes one sphere-backed dynamic or kinematic runtime body handle.
        /// </summary>
        /// <param name="entity">Entity owning the body.</param>
        /// <param name="rigidBody">Authored rigid-body component bound to the runtime handle.</param>
        /// <param name="sphereCollider">Authored sphere collider attached to the entity.</param>
        /// <param name="shapeIndex">Shape index allocated in the active BEPU simulation.</param>
        /// <param name="bodyHandle">BEPU dynamic or kinematic body handle.</param>
        public BepuBodyHandle3D(
            Entity entity,
            RigidBody3DComponent rigidBody,
            SphereCollider3DComponent sphereCollider,
            TypedIndex shapeIndex,
            BodyHandle bodyHandle) {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            RigidBody = rigidBody ?? throw new ArgumentNullException(nameof(rigidBody));
            BoxCollider = null;
            SphereCollider = sphereCollider ?? throw new ArgumentNullException(nameof(sphereCollider));
            StaticMeshCollider = null;
            ShapeIndex = shapeIndex;
            BodyHandle = bodyHandle;
            HasBodyHandle = true;
            StaticHandle = default;
            HasStaticHandle = false;
        }

        /// <summary>
        /// Initializes one sphere-backed static runtime body handle.
        /// </summary>
        /// <param name="entity">Entity owning the body.</param>
        /// <param name="rigidBody">Authored rigid-body component bound to the runtime handle.</param>
        /// <param name="sphereCollider">Authored sphere collider attached to the entity.</param>
        /// <param name="shapeIndex">Shape index allocated in the active BEPU simulation.</param>
        /// <param name="staticHandle">BEPU static handle.</param>
        public BepuBodyHandle3D(
            Entity entity,
            RigidBody3DComponent rigidBody,
            SphereCollider3DComponent sphereCollider,
            TypedIndex shapeIndex,
            StaticHandle staticHandle) {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            RigidBody = rigidBody ?? throw new ArgumentNullException(nameof(rigidBody));
            BoxCollider = null;
            SphereCollider = sphereCollider ?? throw new ArgumentNullException(nameof(sphereCollider));
            StaticMeshCollider = null;
            ShapeIndex = shapeIndex;
            BodyHandle = default;
            HasBodyHandle = false;
            StaticHandle = staticHandle;
            HasStaticHandle = true;
        }

        /// <summary>
        /// Initializes one static-mesh-backed static runtime body handle.
        /// </summary>
        /// <param name="entity">Entity owning the body.</param>
        /// <param name="rigidBody">Authored rigid-body component bound to the runtime handle.</param>
        /// <param name="staticMeshCollider">Authored static mesh collider attached to the entity.</param>
        /// <param name="shapeIndex">Shape index allocated in the active BEPU simulation.</param>
        /// <param name="staticHandle">BEPU static handle.</param>
        public BepuBodyHandle3D(
            Entity entity,
            RigidBody3DComponent rigidBody,
            StaticMeshCollider3DComponent staticMeshCollider,
            TypedIndex shapeIndex,
            StaticHandle staticHandle) {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            RigidBody = rigidBody ?? throw new ArgumentNullException(nameof(rigidBody));
            BoxCollider = null;
            SphereCollider = null;
            StaticMeshCollider = staticMeshCollider ?? throw new ArgumentNullException(nameof(staticMeshCollider));
            ShapeIndex = shapeIndex;
            BodyHandle = default;
            HasBodyHandle = false;
            StaticHandle = staticHandle;
            HasStaticHandle = true;
        }

        /// <summary>
        /// Initializes one fully specified runtime body handle.
        /// </summary>
        /// <param name="entity">Entity owning the body.</param>
        /// <param name="rigidBody">Authored rigid-body component bound to the runtime handle.</param>
        /// <param name="boxCollider">Authored box collider when one is attached.</param>
        /// <param name="sphereCollider">Authored sphere collider when one is attached.</param>
        /// <param name="shapeIndex">Shape index allocated in the active BEPU simulation.</param>
        /// <param name="bodyHandle">BEPU dynamic or kinematic body handle when one was created.</param>
        /// <param name="hasBodyHandle">True when <paramref name="bodyHandle"/> is valid for this entity.</param>
        /// <param name="staticHandle">BEPU static handle when one was created.</param>
        /// <param name="hasStaticHandle">True when <paramref name="staticHandle"/> is valid for this entity.</param>
        public BepuBodyHandle3D(
            Entity entity,
            RigidBody3DComponent rigidBody,
            BoxCollider3DComponent boxCollider,
            SphereCollider3DComponent sphereCollider,
            TypedIndex shapeIndex,
            BodyHandle bodyHandle,
            bool hasBodyHandle,
            StaticHandle staticHandle,
            bool hasStaticHandle) {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            RigidBody = rigidBody ?? throw new ArgumentNullException(nameof(rigidBody));
            BoxCollider = boxCollider;
            SphereCollider = sphereCollider;
            StaticMeshCollider = null;
            ShapeIndex = shapeIndex;
            BodyHandle = bodyHandle;
            HasBodyHandle = hasBodyHandle;
            StaticHandle = staticHandle;
            HasStaticHandle = hasStaticHandle;
        }

        /// <summary>
        /// Gets the owning entity.
        /// </summary>
        public Entity Entity { get; }

        /// <summary>
        /// Gets a value indicating whether the authored rigid body is dynamic.
        /// </summary>
        public bool IsDynamic => RigidBody.BodyKind == BodyKind3D.Dynamic;

        /// <summary>
        /// Gets a value indicating whether the authored rigid body is kinematic.
        /// </summary>
        public bool IsKinematic => RigidBody.BodyKind == BodyKind3D.Kinematic;

        /// <summary>
        /// Gets a value indicating whether the authored rigid body is static.
        /// </summary>
        public bool IsStatic => RigidBody.BodyKind == BodyKind3D.Static;

        /// <summary>
        /// Gets the authored rigid-body component backing this runtime handle.
        /// </summary>
        public RigidBody3DComponent RigidBody { get; }

        /// <summary>
        /// Gets the authored box collider when one is attached.
        /// </summary>
        public BoxCollider3DComponent BoxCollider { get; }

        /// <summary>
        /// Gets the authored sphere collider when one is attached.
        /// </summary>
        public SphereCollider3DComponent SphereCollider { get; }

        /// <summary>
        /// Gets the authored static mesh collider when one is attached.
        /// </summary>
        public StaticMeshCollider3DComponent StaticMeshCollider { get; }

        /// <summary>
        /// Gets the BEPU shape index allocated for this entity.
        /// </summary>
        public TypedIndex ShapeIndex { get; }

        /// <summary>
        /// Gets the BEPU dynamic or kinematic body handle.
        /// </summary>
        public BodyHandle BodyHandle { get; }

        /// <summary>
        /// Gets a value indicating whether <see cref="BodyHandle"/> is valid.
        /// </summary>
        public bool HasBodyHandle { get; }

        /// <summary>
        /// Gets the BEPU static handle.
        /// </summary>
        public StaticHandle StaticHandle { get; }

        /// <summary>
        /// Gets a value indicating whether <see cref="StaticHandle"/> is valid.
        /// </summary>
        public bool HasStaticHandle { get; }
    }
}

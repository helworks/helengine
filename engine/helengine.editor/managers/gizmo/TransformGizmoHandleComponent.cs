namespace helengine.editor {
    /// <summary>
    /// Marks an entity as a transform-gizmo handle and describes its drag constraint.
    /// </summary>
    public class TransformGizmoHandleComponent : Component {
        /// <summary>
        /// Smallest squared vector magnitude accepted as non-zero.
        /// </summary>
        const double MinimumVectorLengthSquared = 0.000000000001;

        /// <summary>
        /// Initializes an axis-constrained handle descriptor.
        /// </summary>
        /// <param name="localAxisDirection">Local-space axis direction before entity orientation is applied.</param>
        public TransformGizmoHandleComponent(float3 localAxisDirection) {
            if (IsNearZero(localAxisDirection)) {
                throw new ArgumentException("Axis direction must be non-zero.", nameof(localAxisDirection));
            }

            ConstraintType = TransformGizmoHandleConstraintType.Axis;
            LocalPrimaryDirection = localAxisDirection;
            LocalSecondaryDirection = float3.Zero;
        }

        /// <summary>
        /// Initializes a plane-constrained handle descriptor.
        /// </summary>
        /// <param name="localPlaneAxisU">First local-space plane direction before entity orientation is applied.</param>
        /// <param name="localPlaneAxisV">Second local-space plane direction before entity orientation is applied.</param>
        public TransformGizmoHandleComponent(float3 localPlaneAxisU, float3 localPlaneAxisV) {
            if (IsNearZero(localPlaneAxisU)) {
                throw new ArgumentException("Plane U axis must be non-zero.", nameof(localPlaneAxisU));
            }

            if (IsNearZero(localPlaneAxisV)) {
                throw new ArgumentException("Plane V axis must be non-zero.", nameof(localPlaneAxisV));
            }

            float3 cross = float3.Cross(localPlaneAxisU, localPlaneAxisV);
            if (IsNearZero(cross)) {
                throw new ArgumentException("Plane axes must not be collinear.", nameof(localPlaneAxisV));
            }

            ConstraintType = TransformGizmoHandleConstraintType.Plane;
            LocalPrimaryDirection = localPlaneAxisU;
            LocalSecondaryDirection = localPlaneAxisV;
        }

        /// <summary>
        /// Gets the constraint type implemented by this handle.
        /// </summary>
        public TransformGizmoHandleConstraintType ConstraintType { get; }

        /// <summary>
        /// Gets the local-space primary direction for this handle.
        /// </summary>
        public float3 LocalPrimaryDirection { get; }

        /// <summary>
        /// Gets the local-space secondary direction for this handle.
        /// </summary>
        public float3 LocalSecondaryDirection { get; }

        /// <summary>
        /// Determines whether a vector is too small to be considered valid.
        /// </summary>
        /// <param name="value">Vector to evaluate.</param>
        /// <returns>True when the vector magnitude is near zero.</returns>
        bool IsNearZero(float3 value) {
            double lengthSquared =
                (value.X * value.X) +
                (value.Y * value.Y) +
                (value.Z * value.Z);
            return lengthSquared <= MinimumVectorLengthSquared;
        }
    }
}

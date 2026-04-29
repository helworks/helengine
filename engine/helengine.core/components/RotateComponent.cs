namespace helengine {
    /// <summary>
    /// Continuously rotates the parent entity around the Y-axis.
    /// </summary>
    public class RotateComponent : UpdateComponent {
        /// <summary>
        /// Applies a small rotation each frame and normalizes orientation.
        /// </summary>
        public override void Update() {
            base.Update();

            // Create a small rotation quaternion for this frame
            float4 deltaRotation;
            float3 axis = new float3(0, 1, 0);
            float4.CreateFromAxisAngle(ref axis, 0.07f, out deltaRotation);

            // Apply rotation
            float4 orientation = Parent.Orientation;
            orientation = deltaRotation * orientation;

            // Normalize to prevent drift
            orientation.Normalize();

            Parent.Orientation = orientation;
        }
    }
}

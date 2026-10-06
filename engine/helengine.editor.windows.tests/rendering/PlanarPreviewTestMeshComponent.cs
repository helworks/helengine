namespace helengine.editor.windows.tests.rendering {
    /// <summary>Uses the editor preview plane contract with a real GPU mesh in offscreen rendering tests.</summary>
    public sealed class PlanarPreviewTestMeshComponent : MeshComponent, IPlanarDrawable3D {
        /// <summary>Gets the transformed normal of the local XY plane.</summary>
        public float3 WorldPlaneNormal => float4.RotateVector(new float3(0f, 0f, 1f), Parent.Orientation);
    }
}

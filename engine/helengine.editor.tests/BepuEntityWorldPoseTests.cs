using BepuPhysics;
using helengine.editor.tests.testing;

namespace helengine.editor.tests;

/// <summary>
/// Verifies BEPU pose generation reads the world-space entity transform.
/// </summary>
public sealed class BepuEntityWorldPoseTests {
    /// <summary>
    /// Ensures parented rigid bodies export one world-space pose instead of the raw local transform.
    /// </summary>
    [Fact]
    public void CreatePose_WithParentedEntity_UsesWorldTransform() {
        InitializeCore();

        Entity parent = new Entity();
        parent.InitChildren();
        parent.LocalPosition = new float3(4f, 0f, 7f);
        float4.CreateFromYawPitchRoll((float)(Math.PI / 2d), 0f, 0f, out float4 parentOrientation);
        parent.LocalOrientation = parentOrientation;

        Entity child = new Entity();
        child.InitChildren();
        child.LocalPosition = new float3(1f, 0f, 0f);
        float4.CreateFromYawPitchRoll(0f, (float)(Math.PI / 6d), 0f, out float4 childOrientation);
        child.LocalOrientation = childOrientation;
        parent.AddChild(child);

        RigidPose pose = BepuEntitySynchronization3D.CreatePose(child);

        AssertApproximately(child.Position, BepuNumericConversion3D.ToHelengineFloat3(pose.Position));
        AssertApproximately(child.Orientation, BepuNumericConversion3D.ToHelengineFloat4(pose.Orientation));
    }

    /// <summary>
    /// Ensures copying one live BEPU world pose back into a parented entity restores the authored local transform instead of writing world values into local fields.
    /// </summary>
    [Fact]
    public void CopyBodyToEntity_WithParentedEntity_RestoresLocalTransformFromWorldPose() {
        InitializeCore();

        Entity parent = new Entity();
        parent.InitChildren();
        parent.LocalPosition = new float3(4f, 2f, 7f);
        parent.LocalScale = new float3(2f, 3f, 4f);
        float4.CreateFromYawPitchRoll((float)(Math.PI / 2d), 0f, 0f, out float4 parentOrientation);
        parent.LocalOrientation = parentOrientation;

        Entity child = new Entity();
        child.InitChildren();
        child.LocalPosition = new float3(1.5f, -0.25f, 0.75f);
        float4.CreateFromYawPitchRoll(0f, (float)(Math.PI / 6d), (float)(Math.PI / 8d), out float4 childOrientation);
        child.LocalOrientation = childOrientation;
        parent.AddChild(child);

        RigidBody3DComponent rigidBody = new RigidBody3DComponent();
        RigidPose pose = BepuEntitySynchronization3D.CreatePose(child);

        BepuEntitySynchronization3D.CopyPoseToEntity(pose, child, rigidBody);

        AssertApproximately(new float3(1.5f, -0.25f, 0.75f), child.LocalPosition);
        AssertApproximately(childOrientation, child.LocalOrientation);
    }

    /// <summary>
    /// Ensures copying one live BEPU pose normalizes the stored quaternion so renderer matrix conversion cannot inherit scale or shear from a non-unit orientation.
    /// </summary>
    [Fact]
    public void CopyPoseToEntity_NormalizesCopiedOrientation() {
        InitializeCore();

        Entity entity = new Entity();
        entity.InitChildren();
        RigidBody3DComponent rigidBody = new RigidBody3DComponent();
        float4.CreateFromYawPitchRoll(0f, (float)(Math.PI / 6d), (float)(Math.PI / 8d), out float4 normalizedOrientation);
        float4 nonNormalizedOrientation = normalizedOrientation * 2f;
        RigidPose pose = new RigidPose(
            BepuNumericConversion3D.ToSystemVector(new float3(1f, 2f, 3f)),
            BepuNumericConversion3D.ToSystemQuaternion(nonNormalizedOrientation));

        BepuEntitySynchronization3D.CopyPoseToEntity(pose, entity, rigidBody);

        AssertApproximately(normalizedOrientation, entity.LocalOrientation);
    }

    /// <summary>
    /// Initializes the minimal core services required by BEPU pose tests.
    /// </summary>
    /// <returns>Initialized test core.</returns>
    static Core InitializeCore() {
        Core core = new Core();
        core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        return core;
    }

    /// <summary>
    /// Asserts two vectors match within a small transform tolerance.
    /// </summary>
    /// <param name="expected">Expected vector.</param>
    /// <param name="actual">Actual vector.</param>
    static void AssertApproximately(float3 expected, float3 actual) {
        Assert.True(Math.Abs(expected.X - actual.X) < 0.0001f);
        Assert.True(Math.Abs(expected.Y - actual.Y) < 0.0001f);
        Assert.True(Math.Abs(expected.Z - actual.Z) < 0.0001f);
    }

    /// <summary>
    /// Asserts two quaternions match within a small transform tolerance.
    /// </summary>
    /// <param name="expected">Expected quaternion.</param>
    /// <param name="actual">Actual quaternion.</param>
    static void AssertApproximately(float4 expected, float4 actual) {
        Assert.True(Math.Abs(expected.X - actual.X) < 0.0001f);
        Assert.True(Math.Abs(expected.Y - actual.Y) < 0.0001f);
        Assert.True(Math.Abs(expected.Z - actual.Z) < 0.0001f);
        Assert.True(Math.Abs(expected.W - actual.W) < 0.0001f);
    }
}

namespace helengine.editor.tests;

/// <summary>
/// Verifies the authored Tilt Trial ball-drive gameplay source keeps the intended runtime synchronization boundaries.
/// </summary>
public sealed class CityTiltTrialBallDriveSourceTests {
    /// <summary>
    /// Ensures the per-frame steering controller uses the current movement defaults and does not force a dynamic-body teleport each update.
    /// </summary>
    [Fact]
    public void City_tilt_trial_stage_controller_source_uses_current_movement_defaults_without_per_frame_dynamic_body_sync() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\game\DemoTiltStageComponent.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("MaximumPlanarSpeed = 11.25f;", source, StringComparison.Ordinal);
        Assert.Contains("PlanarAccelerationUnitsPerSecond = 4.25f;", source, StringComparison.Ordinal);
        Assert.Contains("float3 targetPlanarVelocity = float3.Zero;", source, StringComparison.Ordinal);
        Assert.Contains("ResolveRequiredPhysicsWorld().SynchronizeDynamicBodyVelocity(PlayerSphereEntity);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveRequiredPhysicsWorld().SynchronizeDynamicBody(PlayerSphereEntity);", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the explicit out-of-bounds ball reset still synchronizes the dynamic body after teleporting it back to spawn.
    /// </summary>
    [Fact]
    public void City_tilt_trial_ball_reset_source_keeps_explicit_dynamic_body_sync() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\game\DemoTiltBallResetComponent.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("rigidBody.SetLinearVelocity(float3.Zero);", source, StringComparison.Ordinal);
        Assert.Contains("rigidBody.SetAngularVelocity(float3.Zero);", source, StringComparison.Ordinal);
        Assert.Contains("physicsWorld.SynchronizeDynamicBody(Parent);", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the Tilt Trial follow camera predicts one frame ahead from the tracked ball velocity instead of orbiting the stale pre-physics pose.
    /// </summary>
    [Fact]
    public void City_tilt_trial_follow_camera_source_predicts_orbit_center_from_target_velocity() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\game\DemoTiltFollowCameraComponent.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("UpdateOrder = 1;", source, StringComparison.Ordinal);
        Assert.Contains("core.PredictedPhysicsStepSeconds", source, StringComparison.Ordinal);
        Assert.Contains("ResolvePredictedOrbitCenter(", source, StringComparison.Ordinal);
        Assert.Contains("targetLinearVelocity * (float)elapsedSeconds", source, StringComparison.Ordinal);
    }
}

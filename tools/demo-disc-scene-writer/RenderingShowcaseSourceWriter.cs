namespace helengine.demo_disc_scene_writer {
    /// <summary>
    /// Writes generated user-side runtime motion source files for committed rendering showcase scenes.
    /// </summary>
    public sealed class RenderingShowcaseSourceWriter {
        /// <summary>
        /// Relative authored folder that owns generated rendering showcase source files.
        /// </summary>
        const string RenderingCodeFolderPath = "codebase/rendering";

        /// <summary>
        /// Writes the directional-shadow plaza runtime motion components into the supplied assets root.
        /// </summary>
        /// <param name="assetsRootPath">Assets root path inside the target project.</param>
        public void WriteDirectionalShadowPlazaSources(string assetsRootPath) {
            if (string.IsNullOrWhiteSpace(assetsRootPath)) {
                throw new ArgumentException("Assets root path must be provided.", nameof(assetsRootPath));
            }

            string renderingCodeRootPath = Path.Combine(assetsRootPath, RenderingCodeFolderPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(renderingCodeRootPath);
            File.WriteAllText(Path.Combine(renderingCodeRootPath, "DirectionalShadowTowerSpinComponent.cs"), BuildTowerSpinComponentSource());
            File.WriteAllText(Path.Combine(renderingCodeRootPath, "DirectionalShadowOrbitComponent.cs"), BuildOrbitComponentSource());
            File.WriteAllText(Path.Combine(renderingCodeRootPath, "DirectionalShadowSunSweepComponent.cs"), BuildSunSweepComponentSource());
            File.WriteAllText(Path.Combine(renderingCodeRootPath, "DirectionalShadowCameraOrbitComponent.cs"), BuildCameraOrbitComponentSource());
        }

        /// <summary>
        /// Builds the generated source text for the tower-spin component.
        /// </summary>
        /// <returns>Generated source code for the tower-spin component.</returns>
        string BuildTowerSpinComponentSource() {
            return """
namespace gameplay.rendering {
    /// <summary>
    /// Rotates one plaza tower group around the local Y axis using deterministic absolute time.
    /// </summary>
    public sealed class DirectionalShadowTowerSpinComponent : UpdateComponent {
        /// <summary>
        /// Gets or sets the base yaw offset in radians applied before time-based rotation.
        /// </summary>
        public float BaseYawRadians { get; set; }

        /// <summary>
        /// Gets or sets the angular speed in radians per second.
        /// </summary>
        public float AngularSpeedRadians { get; set; }

        /// <summary>
        /// Evaluates the current orientation from total elapsed runtime time.
        /// </summary>
        public override void Update() {
            base.Update();

            double yawRadians = BaseYawRadians + (AngularSpeedRadians * Core.Instance.TotalElapsedSeconds);
            float4 orientation;
            float4.CreateFromYawPitchRoll((float)yawRadians, 0f, 0f, out orientation);
            orientation.Normalize();
            Parent.LocalOrientation = orientation;
        }
    }
}
""";
        }

        /// <summary>
        /// Builds the generated source text for the orbit component.
        /// </summary>
        /// <returns>Generated source code for the orbit component.</returns>
        string BuildOrbitComponentSource() {
            return """
namespace gameplay.rendering {
    /// <summary>
    /// Moves the parent entity around one authored world-space orbit center using deterministic absolute time.
    /// </summary>
    public sealed class DirectionalShadowOrbitComponent : UpdateComponent {
        /// <summary>
        /// Gets or sets the world-space orbit center.
        /// </summary>
        public float3 OrbitCenter { get; set; }

        /// <summary>
        /// Gets or sets the orbit radius in world units.
        /// </summary>
        public float OrbitRadius { get; set; }

        /// <summary>
        /// Gets or sets the vertical offset applied relative to the orbit center.
        /// </summary>
        public float OrbitHeight { get; set; }

        /// <summary>
        /// Gets or sets the base orbit angle in radians.
        /// </summary>
        public float BaseAngleRadians { get; set; }

        /// <summary>
        /// Gets or sets the angular speed in radians per second.
        /// </summary>
        public float AngularSpeedRadians { get; set; }

        /// <summary>
        /// Evaluates the current orbit position and facing from total elapsed runtime time.
        /// </summary>
        public override void Update() {
            base.Update();

            double angleRadians = BaseAngleRadians + (AngularSpeedRadians * Core.Instance.TotalElapsedSeconds);
            double x = OrbitCenter.X + (Math.Sin(angleRadians) * OrbitRadius);
            double z = OrbitCenter.Z + (Math.Cos(angleRadians) * OrbitRadius);
            Parent.LocalPosition = new float3((float)x, OrbitCenter.Y + OrbitHeight, (float)z);

            float4 orientation;
            float4.CreateFromYawPitchRoll((float)(angleRadians + Math.PI), 0f, 0f, out orientation);
            orientation.Normalize();
            Parent.LocalOrientation = orientation;
        }
    }
}
""";
        }

        /// <summary>
        /// Builds the generated source text for the sun-sweep component.
        /// </summary>
        /// <returns>Generated source code for the sun-sweep component.</returns>
        string BuildSunSweepComponentSource() {
            return """
namespace gameplay.rendering {
    /// <summary>
    /// Sweeps one directional light through a narrow sun arc using a sine wave over absolute runtime time.
    /// </summary>
    public sealed class DirectionalShadowSunSweepComponent : UpdateComponent {
        /// <summary>
        /// Gets or sets the minimum yaw reached by the light sweep in radians.
        /// </summary>
        public float MinYawRadians { get; set; }

        /// <summary>
        /// Gets or sets the maximum yaw reached by the light sweep in radians.
        /// </summary>
        public float MaxYawRadians { get; set; }

        /// <summary>
        /// Gets or sets the fixed pitch applied throughout the sweep in radians.
        /// </summary>
        public float PitchRadians { get; set; }

        /// <summary>
        /// Gets or sets the angular sweep rate in radians per second.
        /// </summary>
        public float SweepSpeedRadians { get; set; }

        /// <summary>
        /// Evaluates the current light orientation from total elapsed runtime time.
        /// </summary>
        public override void Update() {
            base.Update();

            double normalized = (Math.Sin(Core.Instance.TotalElapsedSeconds * SweepSpeedRadians) * 0.5d) + 0.5d;
            double yawRadians = MinYawRadians + ((MaxYawRadians - MinYawRadians) * normalized);
            float4 orientation;
            float4.CreateFromYawPitchRoll((float)yawRadians, PitchRadians, 0f, out orientation);
            orientation.Normalize();
            Parent.LocalOrientation = orientation;
        }
    }
}
""";
        }

        /// <summary>
        /// Builds the generated source text for the camera-orbit component.
        /// </summary>
        /// <returns>Generated source code for the camera-orbit component.</returns>
        string BuildCameraOrbitComponentSource() {
            return """
namespace gameplay.rendering {
    /// <summary>
    /// Keeps the showcase camera on a slow elevated orbit while always looking back toward the plaza center.
    /// </summary>
    public sealed class DirectionalShadowCameraOrbitComponent : UpdateComponent {
        /// <summary>
        /// Gets or sets the world-space orbit center.
        /// </summary>
        public float3 OrbitCenter { get; set; }

        /// <summary>
        /// Gets or sets the orbit radius in world units.
        /// </summary>
        public float OrbitRadius { get; set; }

        /// <summary>
        /// Gets or sets the vertical offset applied relative to the orbit center.
        /// </summary>
        public float OrbitHeight { get; set; }

        /// <summary>
        /// Gets or sets the base orbit angle in radians.
        /// </summary>
        public float BaseAngleRadians { get; set; }

        /// <summary>
        /// Gets or sets the angular speed in radians per second.
        /// </summary>
        public float AngularSpeedRadians { get; set; }

        /// <summary>
        /// Gets or sets the fixed downward camera pitch in radians.
        /// </summary>
        public float LookDownPitchRadians { get; set; }

        /// <summary>
        /// Evaluates the current camera orbit position and inward-facing orientation from total elapsed runtime time.
        /// </summary>
        public override void Update() {
            base.Update();

            double angleRadians = BaseAngleRadians + (AngularSpeedRadians * Core.Instance.TotalElapsedSeconds);
            double x = OrbitCenter.X + (Math.Sin(angleRadians) * OrbitRadius);
            double z = OrbitCenter.Z + (Math.Cos(angleRadians) * OrbitRadius);
            Parent.LocalPosition = new float3((float)x, OrbitCenter.Y + OrbitHeight, (float)z);

            double inwardYawRadians = Math.Atan2(OrbitCenter.X - x, OrbitCenter.Z - z);
            float4 orientation;
            float4.CreateFromYawPitchRoll((float)inwardYawRadians, LookDownPitchRadians, 0f, out orientation);
            orientation.Normalize();
            Parent.LocalOrientation = orientation;
        }
    }
}
""";
        }
    }
}

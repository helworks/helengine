using helengine.editor;
using helengine.files;

namespace helengine.demo_disc_scene_writer {
    /// <summary>
    /// Writes serialized scene component records for generated user-side rendering showcase components.
    /// </summary>
    public static class RenderingScriptComponentRecordFactory {
        /// <summary>
        /// Stable serialized component type id for the tower-spin runtime component.
        /// </summary>
        const string TowerSpinTypeId = "gameplay.rendering.DirectionalShadowTowerSpinComponent, gameplay";

        /// <summary>
        /// Stable serialized component type id for the orbit runtime component.
        /// </summary>
        const string OrbitTypeId = "gameplay.rendering.DirectionalShadowOrbitComponent, gameplay";

        /// <summary>
        /// Stable serialized component type id for the sun-sweep runtime component.
        /// </summary>
        const string SunSweepTypeId = "gameplay.rendering.DirectionalShadowSunSweepComponent, gameplay";

        /// <summary>
        /// Stable serialized component type id for the camera-orbit runtime component.
        /// </summary>
        const string CameraOrbitTypeId = "gameplay.rendering.DirectionalShadowCameraOrbitComponent, gameplay";

        /// <summary>
        /// Creates one serialized tower-spin component record.
        /// </summary>
        /// <param name="componentIndex">Entity-local component index.</param>
        /// <param name="baseYawRadians">Base yaw offset in radians.</param>
        /// <param name="angularSpeedRadians">Angular speed in radians per second.</param>
        /// <returns>Serialized scene component record for the tower-spin component.</returns>
        public static SceneComponentAssetRecord CreateTowerSpinRecord(int componentIndex, float baseYawRadians, float angularSpeedRadians) {
            if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be non-negative.");
            }

            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("AngularSpeedRadians", fieldWriter => fieldWriter.WriteSingle(angularSpeedRadians));
            writer.WriteField("BaseYawRadians", fieldWriter => fieldWriter.WriteSingle(baseYawRadians));
            return new SceneComponentAssetRecord {
                ComponentTypeId = TowerSpinTypeId,
                ComponentIndex = componentIndex,
                Payload = writer.BuildPayload()
            };
        }

        /// <summary>
        /// Creates one serialized orbit component record.
        /// </summary>
        /// <param name="componentIndex">Entity-local component index.</param>
        /// <param name="orbitCenter">World-space orbit center.</param>
        /// <param name="orbitRadius">Orbit radius in world units.</param>
        /// <param name="orbitHeight">Vertical orbit offset relative to the center.</param>
        /// <param name="baseAngleRadians">Base orbit angle in radians.</param>
        /// <param name="angularSpeedRadians">Angular speed in radians per second.</param>
        /// <returns>Serialized scene component record for the orbit component.</returns>
        public static SceneComponentAssetRecord CreateOrbitRecord(
            int componentIndex,
            float3 orbitCenter,
            float orbitRadius,
            float orbitHeight,
            float baseAngleRadians,
            float angularSpeedRadians) {
            if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be non-negative.");
            } else if (orbitRadius <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(orbitRadius), "Orbit radius must be greater than zero.");
            }

            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("AngularSpeedRadians", fieldWriter => fieldWriter.WriteSingle(angularSpeedRadians));
            writer.WriteField("BaseAngleRadians", fieldWriter => fieldWriter.WriteSingle(baseAngleRadians));
            writer.WriteField("OrbitCenter", fieldWriter => fieldWriter.WriteFloat3(orbitCenter));
            writer.WriteField("OrbitHeight", fieldWriter => fieldWriter.WriteSingle(orbitHeight));
            writer.WriteField("OrbitRadius", fieldWriter => fieldWriter.WriteSingle(orbitRadius));
            return new SceneComponentAssetRecord {
                ComponentTypeId = OrbitTypeId,
                ComponentIndex = componentIndex,
                Payload = writer.BuildPayload()
            };
        }

        /// <summary>
        /// Creates one serialized sun-sweep component record.
        /// </summary>
        /// <param name="componentIndex">Entity-local component index.</param>
        /// <param name="minYawRadians">Minimum authored yaw in radians.</param>
        /// <param name="maxYawRadians">Maximum authored yaw in radians.</param>
        /// <param name="pitchRadians">Fixed pitch in radians.</param>
        /// <param name="sweepSpeedRadians">Sweep rate in radians per second.</param>
        /// <returns>Serialized scene component record for the sun-sweep component.</returns>
        public static SceneComponentAssetRecord CreateSunSweepRecord(
            int componentIndex,
            float minYawRadians,
            float maxYawRadians,
            float pitchRadians,
            float sweepSpeedRadians) {
            if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be non-negative.");
            }

            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("MaxYawRadians", fieldWriter => fieldWriter.WriteSingle(maxYawRadians));
            writer.WriteField("MinYawRadians", fieldWriter => fieldWriter.WriteSingle(minYawRadians));
            writer.WriteField("PitchRadians", fieldWriter => fieldWriter.WriteSingle(pitchRadians));
            writer.WriteField("SweepSpeedRadians", fieldWriter => fieldWriter.WriteSingle(sweepSpeedRadians));
            return new SceneComponentAssetRecord {
                ComponentTypeId = SunSweepTypeId,
                ComponentIndex = componentIndex,
                Payload = writer.BuildPayload()
            };
        }

        /// <summary>
        /// Creates one serialized camera-orbit component record.
        /// </summary>
        /// <param name="componentIndex">Entity-local component index.</param>
        /// <param name="orbitCenter">World-space orbit center.</param>
        /// <param name="orbitRadius">Orbit radius in world units.</param>
        /// <param name="orbitHeight">Vertical orbit offset relative to the center.</param>
        /// <param name="baseAngleRadians">Base orbit angle in radians.</param>
        /// <param name="angularSpeedRadians">Angular speed in radians per second.</param>
        /// <param name="lookDownPitchRadians">Fixed downward pitch in radians.</param>
        /// <returns>Serialized scene component record for the camera-orbit component.</returns>
        public static SceneComponentAssetRecord CreateCameraOrbitRecord(
            int componentIndex,
            float3 orbitCenter,
            float orbitRadius,
            float orbitHeight,
            float baseAngleRadians,
            float angularSpeedRadians,
            float lookDownPitchRadians) {
            if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be non-negative.");
            } else if (orbitRadius <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(orbitRadius), "Orbit radius must be greater than zero.");
            }

            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("AngularSpeedRadians", fieldWriter => fieldWriter.WriteSingle(angularSpeedRadians));
            writer.WriteField("BaseAngleRadians", fieldWriter => fieldWriter.WriteSingle(baseAngleRadians));
            writer.WriteField("LookDownPitchRadians", fieldWriter => fieldWriter.WriteSingle(lookDownPitchRadians));
            writer.WriteField("OrbitCenter", fieldWriter => fieldWriter.WriteFloat3(orbitCenter));
            writer.WriteField("OrbitHeight", fieldWriter => fieldWriter.WriteSingle(orbitHeight));
            writer.WriteField("OrbitRadius", fieldWriter => fieldWriter.WriteSingle(orbitRadius));
            return new SceneComponentAssetRecord {
                ComponentTypeId = CameraOrbitTypeId,
                ComponentIndex = componentIndex,
                Payload = writer.BuildPayload()
            };
        }
    }
}

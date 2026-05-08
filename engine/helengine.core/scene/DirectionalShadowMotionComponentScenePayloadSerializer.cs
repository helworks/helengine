namespace helengine {
    /// <summary>
    /// Reads and writes scene payloads for built-in directional-shadow motion showcase components.
    /// </summary>
    public static class DirectionalShadowMotionComponentScenePayloadSerializer {
        /// <summary>
        /// Current payload version shared by all directional-shadow motion component records.
        /// </summary>
        public const byte CurrentVersion = 1;

        /// <summary>
        /// Writes the camera-orbit payload fields into the supplied writer.
        /// </summary>
        /// <param name="writer">Destination writer receiving the payload.</param>
        /// <param name="component">Camera-orbit component whose values should be serialized.</param>
        public static void WriteCameraOrbit(EngineBinaryWriter writer, DirectionalShadowCameraOrbitComponent component) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            WriteOrbitFields(
                writer,
                component.OrbitCenter,
                component.OrbitRadius,
                component.OrbitHeight,
                component.BaseAngleRadians,
                component.AngularSpeedRadians);
            writer.WriteSingle(component.LookDownPitchRadians);
        }

        /// <summary>
        /// Reads the camera-orbit payload fields from the supplied reader.
        /// </summary>
        /// <param name="reader">Source reader positioned at the camera-orbit payload.</param>
        /// <returns>Camera-orbit component reconstructed from the payload.</returns>
        public static DirectionalShadowCameraOrbitComponent ReadCameraOrbit(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            DirectionalShadowCameraOrbitComponent component = new DirectionalShadowCameraOrbitComponent();
            ReadOrbitFields(
                reader,
                out float3 orbitCenter,
                out float orbitRadius,
                out float orbitHeight,
                out float baseAngleRadians,
                out float angularSpeedRadians);
            component.OrbitCenter = orbitCenter;
            component.OrbitRadius = orbitRadius;
            component.OrbitHeight = orbitHeight;
            component.BaseAngleRadians = baseAngleRadians;
            component.AngularSpeedRadians = angularSpeedRadians;
            component.LookDownPitchRadians = reader.ReadSingle();
            return component;
        }

        /// <summary>
        /// Writes the orbit payload fields into the supplied writer.
        /// </summary>
        /// <param name="writer">Destination writer receiving the payload.</param>
        /// <param name="component">Orbit component whose values should be serialized.</param>
        public static void WriteOrbit(EngineBinaryWriter writer, DirectionalShadowOrbitComponent component) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            WriteOrbitFields(
                writer,
                component.OrbitCenter,
                component.OrbitRadius,
                component.OrbitHeight,
                component.BaseAngleRadians,
                component.AngularSpeedRadians);
        }

        /// <summary>
        /// Reads the orbit payload fields from the supplied reader.
        /// </summary>
        /// <param name="reader">Source reader positioned at the orbit payload.</param>
        /// <returns>Orbit component reconstructed from the payload.</returns>
        public static DirectionalShadowOrbitComponent ReadOrbit(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            DirectionalShadowOrbitComponent component = new DirectionalShadowOrbitComponent();
            ReadOrbitFields(
                reader,
                out float3 orbitCenter,
                out float orbitRadius,
                out float orbitHeight,
                out float baseAngleRadians,
                out float angularSpeedRadians);
            component.OrbitCenter = orbitCenter;
            component.OrbitRadius = orbitRadius;
            component.OrbitHeight = orbitHeight;
            component.BaseAngleRadians = baseAngleRadians;
            component.AngularSpeedRadians = angularSpeedRadians;
            return component;
        }

        /// <summary>
        /// Writes the sun-sweep payload fields into the supplied writer.
        /// </summary>
        /// <param name="writer">Destination writer receiving the payload.</param>
        /// <param name="component">Sun-sweep component whose values should be serialized.</param>
        public static void WriteSunSweep(EngineBinaryWriter writer, DirectionalShadowSunSweepComponent component) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            writer.WriteSingle(component.MinYawRadians);
            writer.WriteSingle(component.MaxYawRadians);
            writer.WriteSingle(component.PitchRadians);
            writer.WriteSingle(component.SweepSpeedRadians);
        }

        /// <summary>
        /// Reads the sun-sweep payload fields from the supplied reader.
        /// </summary>
        /// <param name="reader">Source reader positioned at the sun-sweep payload.</param>
        /// <returns>Sun-sweep component reconstructed from the payload.</returns>
        public static DirectionalShadowSunSweepComponent ReadSunSweep(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new DirectionalShadowSunSweepComponent {
                MinYawRadians = reader.ReadSingle(),
                MaxYawRadians = reader.ReadSingle(),
                PitchRadians = reader.ReadSingle(),
                SweepSpeedRadians = reader.ReadSingle()
            };
        }

        /// <summary>
        /// Writes the tower-spin payload fields into the supplied writer.
        /// </summary>
        /// <param name="writer">Destination writer receiving the payload.</param>
        /// <param name="component">Tower-spin component whose values should be serialized.</param>
        public static void WriteTowerSpin(EngineBinaryWriter writer, DirectionalShadowTowerSpinComponent component) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            writer.WriteSingle(component.BaseYawRadians);
            writer.WriteSingle(component.AngularSpeedRadians);
        }

        /// <summary>
        /// Reads the tower-spin payload fields from the supplied reader.
        /// </summary>
        /// <param name="reader">Source reader positioned at the tower-spin payload.</param>
        /// <returns>Tower-spin component reconstructed from the payload.</returns>
        public static DirectionalShadowTowerSpinComponent ReadTowerSpin(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new DirectionalShadowTowerSpinComponent {
                BaseYawRadians = reader.ReadSingle(),
                AngularSpeedRadians = reader.ReadSingle()
            };
        }

        /// <summary>
        /// Writes the shared orbit payload fields used by both orbit motion families.
        /// </summary>
        /// <param name="writer">Destination writer receiving the payload.</param>
        /// <param name="orbitCenter">Authored world-space orbit center.</param>
        /// <param name="orbitRadius">Authored orbit radius.</param>
        /// <param name="orbitHeight">Authored orbit height.</param>
        /// <param name="baseAngleRadians">Authored base orbit angle.</param>
        /// <param name="angularSpeedRadians">Authored angular speed.</param>
        static void WriteOrbitFields(
            EngineBinaryWriter writer,
            float3 orbitCenter,
            float orbitRadius,
            float orbitHeight,
            float baseAngleRadians,
            float angularSpeedRadians) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            WriteFloat3(writer, orbitCenter);
            writer.WriteSingle(orbitRadius);
            writer.WriteSingle(orbitHeight);
            writer.WriteSingle(baseAngleRadians);
            writer.WriteSingle(angularSpeedRadians);
        }

        /// <summary>
        /// Reads the shared orbit payload fields used by both orbit motion families.
        /// </summary>
        /// <param name="reader">Source reader positioned at the orbit payload.</param>
        /// <param name="orbitCenter">Decoded world-space orbit center.</param>
        /// <param name="orbitRadius">Decoded orbit radius.</param>
        /// <param name="orbitHeight">Decoded orbit height.</param>
        /// <param name="baseAngleRadians">Decoded base orbit angle.</param>
        /// <param name="angularSpeedRadians">Decoded angular speed.</param>
        static void ReadOrbitFields(
            EngineBinaryReader reader,
            out float3 orbitCenter,
            out float orbitRadius,
            out float orbitHeight,
            out float baseAngleRadians,
            out float angularSpeedRadians) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            orbitCenter = ReadFloat3(reader);
            orbitRadius = reader.ReadSingle();
            orbitHeight = reader.ReadSingle();
            baseAngleRadians = reader.ReadSingle();
            angularSpeedRadians = reader.ReadSingle();
        }

        /// <summary>
        /// Writes one <see cref="float3"/> value into the supplied writer.
        /// </summary>
        /// <param name="writer">Destination writer receiving the vector payload.</param>
        /// <param name="value">Vector value that should be serialized.</param>
        static void WriteFloat3(EngineBinaryWriter writer, float3 value) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteSingle(value.X);
            writer.WriteSingle(value.Y);
            writer.WriteSingle(value.Z);
        }

        /// <summary>
        /// Reads one <see cref="float3"/> value from the supplied reader.
        /// </summary>
        /// <param name="reader">Source reader positioned at the vector payload.</param>
        /// <returns>Decoded vector value.</returns>
        static float3 ReadFloat3(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new float3(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
        }
    }
}

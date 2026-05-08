namespace helengine {
    /// <summary>
    /// Reads and writes packaged runtime payloads for the built-in directional-shadow motion components.
    /// </summary>
    public static class DirectionalShadowMotionComponentScenePayloadSerializer {
        /// <summary>
        /// Current payload version shared by all serialized directional-shadow motion component records.
        /// </summary>
        public const byte CurrentVersion = 1;

        /// <summary>
        /// Writes one camera-orbit component payload into the supplied writer.
        /// </summary>
        /// <param name="writer">Destination writer receiving the payload.</param>
        /// <param name="component">Camera-orbit component whose values should be serialized.</param>
        public static void WriteCameraOrbit(EngineBinaryWriter writer, DirectionalShadowCameraOrbitComponent component) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            WriteFloat3(writer, component.OrbitCenter);
            writer.WriteSingle(component.OrbitRadius);
            writer.WriteSingle(component.OrbitHeight);
            writer.WriteSingle(component.BaseAngleRadians);
            writer.WriteSingle(component.AngularSpeedRadians);
            writer.WriteSingle(component.LookDownPitchRadians);
        }

        /// <summary>
        /// Reads one camera-orbit component payload from the supplied reader.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Reconstructed camera-orbit component.</returns>
        public static DirectionalShadowCameraOrbitComponent ReadCameraOrbit(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new DirectionalShadowCameraOrbitComponent {
                OrbitCenter = ReadFloat3(reader),
                OrbitRadius = reader.ReadSingle(),
                OrbitHeight = reader.ReadSingle(),
                BaseAngleRadians = reader.ReadSingle(),
                AngularSpeedRadians = reader.ReadSingle(),
                LookDownPitchRadians = reader.ReadSingle()
            };
        }

        /// <summary>
        /// Writes one orbit component payload into the supplied writer.
        /// </summary>
        /// <param name="writer">Destination writer receiving the payload.</param>
        /// <param name="component">Orbit component whose values should be serialized.</param>
        public static void WriteOrbit(EngineBinaryWriter writer, DirectionalShadowOrbitComponent component) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            WriteFloat3(writer, component.OrbitCenter);
            writer.WriteSingle(component.OrbitRadius);
            writer.WriteSingle(component.OrbitHeight);
            writer.WriteSingle(component.BaseAngleRadians);
            writer.WriteSingle(component.AngularSpeedRadians);
        }

        /// <summary>
        /// Reads one orbit component payload from the supplied reader.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Reconstructed orbit component.</returns>
        public static DirectionalShadowOrbitComponent ReadOrbit(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new DirectionalShadowOrbitComponent {
                OrbitCenter = ReadFloat3(reader),
                OrbitRadius = reader.ReadSingle(),
                OrbitHeight = reader.ReadSingle(),
                BaseAngleRadians = reader.ReadSingle(),
                AngularSpeedRadians = reader.ReadSingle()
            };
        }

        /// <summary>
        /// Writes one sun-sweep component payload into the supplied writer.
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
        /// Reads one sun-sweep component payload from the supplied reader.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Reconstructed sun-sweep component.</returns>
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
        /// Writes one tower-spin component payload into the supplied writer.
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
        /// Reads one tower-spin component payload from the supplied reader.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Reconstructed tower-spin component.</returns>
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
